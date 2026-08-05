"""Núcleo de ML: ingeniería de características, entrenamiento y predicción.

El modelo predice la probabilidad de un pico de glucosa (y su nivel de riesgo)
en las próximas 2 horas a partir de la serie temporal de lecturas del sensor
(pulso, temperatura, sudoración y probabilidad de pico reportada por el sensor).

El microservicio es agnóstico a la base de datos: .NET le envía las lecturas
históricas y los eventos metabólicos como JSON.
"""

import math
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path

import joblib
import numpy as np
from sklearn.ensemble import GradientBoostingClassifier
from sklearn.metrics import accuracy_score, f1_score, precision_score, recall_score
from sklearn.model_selection import train_test_split

from .schemas import PredictResponse, Lectura

WINDOW_SIZE = 12
STRIDE = 3
HORIZON_HOURS = 2
MIN_SAMPLES = 30
RISK_LEVELS = {"Pre-Pico", "Critico"}

MODEL_DIR = Path(__file__).resolve().parent.parent / "model_data"
MODEL_PATH = MODEL_DIR / "model.pkl"
VERSION_PATH = MODEL_DIR / "version.txt"

FEATURES = [
    "pulso_last", "pulso_mean", "pulso_std", "pulso_slope",
    "temp_last", "temp_mean", "temp_std", "temp_slope",
    "sudor_last", "sudor_mean", "sudor_std", "sudor_slope",
    "prob_last", "prob_mean", "prob_std", "prob_slope",
    "hour_sin", "hour_cos", "n",
]


def _naive_utc(dt: datetime) -> datetime:
    if dt.tzinfo is not None:
        dt = dt.astimezone(timezone.utc).replace(tzinfo=None)
    return dt


def _slope(x, y) -> float:
    if len(x) < 2:
        return 0.0
    xa = np.asarray(x, dtype=float)
    ya = np.asarray(y, dtype=float)
    if xa.max() == xa.min():
        return 0.0
    return float(np.polyfit(xa, ya, 1)[0])


def to_row(lectura: Lectura) -> dict:
    ts = _naive_utc(lectura.timestamp)
    return {
        "paciente_id": lectura.paciente_id,
        "ts": ts,
        "pulso_bpm": lectura.pulso_bpm,
        "temperatura_c": lectura.temperatura_c,
        "sudoracion_gsr": lectura.sudoracion_gsr,
        "probabilidad_pico": lectura.probabilidad_pico,
    }


def _window_features(rows: list[dict]) -> dict:
    n = len(rows)
    t0 = rows[0]["ts"]
    minutes = [(r["ts"] - t0).total_seconds() / 60.0 for r in rows]
    feats: dict = {}
    for key, prefix in (
        ("pulso_bpm", "pulso"),
        ("temperatura_c", "temp"),
        ("sudoracion_gsr", "sudor"),
        ("probabilidad_pico", "prob"),
    ):
        vals = [r[key] for r in rows]
        feats[f"{prefix}_last"] = float(vals[-1])
        feats[f"{prefix}_mean"] = float(np.mean(vals))
        feats[f"{prefix}_std"] = float(np.std(vals)) if n > 1 else 0.0
        feats[f"{prefix}_slope"] = _slope(minutes, vals)
    hour = rows[-1]["ts"].hour
    feats["hour_sin"] = math.sin(2 * math.pi * hour / 24)
    feats["hour_cos"] = math.cos(2 * math.pi * hour / 24)
    feats["n"] = n
    return feats


def _build_dataset(lecturas: list[Lectura], eventos: list) -> tuple[list, list]:
    by_patient: dict[str, list[dict]] = {}
    for l in lecturas:
        row = to_row(l)
        by_patient.setdefault(row["paciente_id"], []).append(row)

    event_times: dict[str, list[datetime]] = {}
    for e in eventos:
        if e.nivel_riesgo in RISK_LEVELS:
            ts = _naive_utc(e.fecha_evento)
            event_times.setdefault(e.paciente_id, []).append(ts)

    X: list[dict] = []
    y: list[int] = []
    for pid, rows in by_patient.items():
        rows.sort(key=lambda r: r["ts"])
        if len(rows) < WINDOW_SIZE:
            continue
        evts = sorted(event_times.get(pid, []))
        idx = 0
        while idx + WINDOW_SIZE <= len(rows):
            window = rows[idx:idx + WINDOW_SIZE]
            end = window[-1]["ts"]
            label = 1 if any(end <= t <= end + timedelta(hours=HORIZON_HOURS) for t in evts) else 0
            X.append(_window_features(window))
            y.append(label)
            idx += STRIDE
    return X, y


def train(lecturas: list[Lectura], eventos: list, model_version: str) -> tuple[dict, int]:
    X, y = _build_dataset(lecturas, eventos)
    if len(X) < MIN_SAMPLES:
        raise ValueError("datos insuficientes")

    X_arr = np.asarray([[row[f] for f in FEATURES] for row in X], dtype=float)
    y_arr = np.asarray(y, dtype=int)

    clf = GradientBoostingClassifier(
        max_depth=3, n_estimators=300, learning_rate=0.08,
        subsample=0.9, random_state=42,
    )
    X_tr, X_te, y_tr, y_te = train_test_split(
        X_arr, y_arr, test_size=0.2, stratify=y_arr, random_state=42
    )
    clf.fit(X_tr, y_tr)
    pred = clf.predict(X_te)

    metrics = {
        "accuracy": float(accuracy_score(y_te, pred)),
        "precision": float(precision_score(y_te, pred, zero_division=0)),
        "recall": float(recall_score(y_te, pred, zero_division=0)),
        "f1_score": float(f1_score(y_te, pred, zero_division=0)),
    }
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    joblib.dump(clf, MODEL_PATH)
    VERSION_PATH.write_text(model_version)
    return metrics, len(X_arr)


@dataclass(frozen=True)
class RangoVital:
    etiqueta: str
    minimo_saludable: float
    maximo_saludable: float
    minimo_extremo: float
    maximo_extremo: float
    peso: float


FALLBACK_PESO_PEOR_SENAL = 0.6

FALLBACK_RANGES: dict[str, RangoVital] = {
    "pulso_bpm": RangoVital("Pulso", 60.0, 100.0, 35.0, 170.0, 0.50),
    "temperatura_c": RangoVital("Temperatura", 36.0, 37.5, 34.0, 42.0, 0.30),
    "sudoracion_gsr": RangoVital("Sudoración (GSR)", 0.5, 4.0, 0.0, 12.0, 0.20),
}


def _severidad(valor: float, rango: RangoVital) -> float:
    if valor < rango.minimo_saludable:
        denom = rango.minimo_saludable - rango.minimo_extremo
        if denom <= 0:
            return 1.0
        return max(0.0, min(1.0, (rango.minimo_saludable - valor) / denom))
    if valor > rango.maximo_saludable:
        denom = rango.maximo_extremo - rango.maximo_saludable
        if denom <= 0:
            return 1.0
        return max(0.0, min(1.0, (valor - rango.maximo_saludable) / denom))
    return 0.0


def _fallback_scoring(rows: list[dict]) -> tuple[float, list[dict]]:
    if not rows:
        return 0.5, []

    ultima = rows[-1]
    contribuciones: list[dict] = []
    severidad_ponderada = 0.0
    peso_total = 0.0
    max_severidad = 0.0

    for clave, rango in FALLBACK_RANGES.items():
        valor = float(ultima[clave])
        severidad = _severidad(valor, rango)
        contribuciones.append(
            {"senal": rango.etiqueta, "valor": round(valor, 4), "severidad": round(severidad, 4)}
        )
        severidad_ponderada += rango.peso * severidad
        peso_total += rango.peso
        max_severidad = max(max_severidad, severidad)

    severidad_media = severidad_ponderada / peso_total
    efectiva = (
        FALLBACK_PESO_PEOR_SENAL * max_severidad
        + (1.0 - FALLBACK_PESO_PEOR_SENAL) * severidad_media
    )
    logit = 6.0 * efectiva - 3.0
    probabilidad = 1.0 / (1.0 + math.exp(-logit))
    return round(probabilidad, 4), contribuciones


def fallback_probability(rows: list[dict]) -> float:
    """Probabilidad de fallback basada en rangos vitales (no usa probabilidad_pico)."""
    prob, _ = _fallback_scoring(rows)
    return prob


def clasificar(prob: float) -> tuple[str, int | None, str]:
    if prob >= 0.75:
        return (
            "Critico",
            1,
            "Pico de glucosa inminente. Contactar al cuidador y verificar niveles de inmediato.",
        )
    if prob >= 0.5:
        return (
            "Pre-Pico",
            2,
            "Riesgo elevado de pico en las próximas 2 horas. Mantener hidratación y monitorear signos.",
        )
    return "Normal", None, "Sin riesgo inminente. Mantener el monitoreo habitual."


def predict(paciente_id: str, lecturas: list[Lectura]) -> PredictResponse:
    now = datetime.now(timezone.utc)
    rows = sorted((to_row(l) for l in lecturas), key=lambda r: r["ts"])

    if len(rows) >= WINDOW_SIZE and MODEL_PATH.exists():
        window = rows[-WINDOW_SIZE:]
        feats = _window_features(window)
        clf = joblib.load(MODEL_PATH)
        x = np.array([[feats[f] for f in FEATURES]], dtype=float)
        prob = float(clf.predict_proba(x)[0, 1])
        version = VERSION_PATH.read_text().strip() if VERSION_PATH.exists() else "desconocida"
        contribuciones = None
        explicacion = "Modelo entrenado: GradientBoosting sobre ventanas deslizantes (12 lecturas, horizonte 2 h)."
    else:
        prob, contribuciones = _fallback_scoring(rows)
        version = "fallback"
        explicacion = "Fallback baseline: severidad por rangos vitales (pulso, temperatura, sudoración) → probabilidad logística."

    nivel, horas, rec = clasificar(prob)
    return PredictResponse(
        paciente_id=paciente_id,
        probabilidad_pico=round(prob, 4),
        nivel_riesgo=nivel,
        horas_estimadas=horas,
        recomendacion=rec,
        modelo_version=version,
        fecha_prediccion=now,
        fecha_expiracion=now + timedelta(hours=HORIZON_HOURS),
        contribuciones=contribuciones,
        explicacion=explicacion,
    )
