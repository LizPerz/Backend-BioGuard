from datetime import datetime

from pydantic import BaseModel


class Lectura(BaseModel):
    paciente_id: str
    timestamp: datetime
    pulso_bpm: int
    temperatura_c: float
    sudoracion_gsr: float
    probabilidad_pico: float


class Evento(BaseModel):
    paciente_id: str
    nivel_riesgo: str
    fecha_evento: datetime


class TrainRequest(BaseModel):
    lecturas: list[Lectura] = []
    eventos: list[Evento] = []
    model_version: str = "1.0.0"


class TrainResponse(BaseModel):
    model_version: str
    accuracy: float
    precision: float
    recall: float
    f1_score: float
    total_muestras: int
    activo: bool = True


class PredictRequest(BaseModel):
    paciente_id: str
    lecturas: list[Lectura] = []


class Contribucion(BaseModel):
    senal: str
    valor: float
    severidad: float


class PredictResponse(BaseModel):
    paciente_id: str
    probabilidad_pico: float
    nivel_riesgo: str
    horas_estimadas: int | None = None
    recomendacion: str
    modelo_version: str
    fecha_prediccion: datetime
    fecha_expiracion: datetime
    contribuciones: list[Contribucion] | None = None
    explicacion: str | None = None
