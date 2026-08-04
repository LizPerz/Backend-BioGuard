import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fastapi.testclient import TestClient  # noqa: E402

from app.main import app  # noqa: E402

client = TestClient(app)


def test_health_returns_200():
    r = client.get("/health")
    assert r.status_code == 200
    body = r.json()
    assert body["status"] == "ok"
    assert body["service"] == "bioguard-ml"


def test_predict_returns_200_sin_lecturas():
    r = client.post("/api/v1/predicciones", json={"paciente_id": "p1", "lecturas": []})
    assert r.status_code == 200
    body = r.json()
    assert body["paciente_id"] == "p1"
    assert body["nivel_riesgo"] in ("Normal", "Pre-Pico", "Critico")
    assert 0.0 <= body["probabilidad_pico"] <= 1.0


def test_predict_returns_200_con_fallback():
    base = datetime.now(timezone.utc)
    lecturas = [
        {
            "paciente_id": "p1",
            "timestamp": (base - timedelta(minutes=(6 - i) * 5)).isoformat(),
            "pulso_bpm": 110 if i % 3 == 0 else 80,
            "temperatura_c": 37.8 if i % 3 == 0 else 36.5,
            "sudoracion_gsr": 9.5 if i % 3 == 0 else 3.0,
            "probabilidad_pico": 0.8 if i % 3 == 0 else 0.2,
        }
        for i in range(6)
    ]
    r = client.post("/api/v1/predicciones", json={"paciente_id": "p1", "lecturas": lecturas})
    assert r.status_code == 200
    assert r.json()["modelo_version"] == "fallback"


def test_train_y_predict_ciclo_completo():
    import random

    random.seed(42)
    base = datetime.now(timezone.utc) - timedelta(days=30)
    lecturas = []
    for i in range(600):
        t = base + timedelta(minutes=i * 15)
        pre = (i // 10) % 3 == 0
        lecturas.append(
            {
                "paciente_id": "p1",
                "timestamp": t.isoformat(),
                "pulso_bpm": random.randint(100, 120) if pre else random.randint(65, 95),
                "temperatura_c": round(37.5 + random.random() * 0.8, 2) if pre else round(36.2 + random.random() * 0.6, 2),
                "sudoracion_gsr": round(8.0 + random.random() * 4.0, 2) if pre else round(2.0 + random.random() * 3.0, 2),
                "probabilidad_pico": round(0.75 + random.random() * 0.2, 2) if pre else round(0.1 + random.random() * 0.3, 2),
            }
        )
    eventos = [
        {
            "paciente_id": "p1",
            "nivel_riesgo": "Pre-Pico",
            "fecha_evento": (base + timedelta(minutes=i * 15) + timedelta(minutes=90)).isoformat(),
        }
        for i in range(0, 600, 30)
    ]

    tr = client.post(
        "/api/v1/train",
        json={"lecturas": lecturas, "eventos": eventos, "model_version": "test-1.0.0"},
    )
    assert tr.status_code == 200
    m = tr.json()
    assert m["total_muestras"] > 0
    assert 0.0 <= m["accuracy"] <= 1.0

    pr = client.post(
        "/api/v1/predicciones",
        json={"paciente_id": "p1", "lecturas": lecturas[-12:]},
    )
    assert pr.status_code == 200
    body = pr.json()
    assert body["modelo_version"] == "test-1.0.0"
    assert body["nivel_riesgo"] in ("Normal", "Pre-Pico", "Critico")
