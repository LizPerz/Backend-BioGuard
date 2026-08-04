from fastapi import FastAPI, HTTPException

from . import ml
from .schemas import PredictRequest, PredictResponse, TrainRequest, TrainResponse

app = FastAPI(
    title="BioGuard ML Service",
    description="Microservicio de predicción de picos de glucosa para BioGuard.",
    version="1.0.0",
)


@app.get("/health")
def health() -> dict:
    return {
        "status": "ok",
        "service": "bioguard-ml",
        "model_loaded": ml.MODEL_PATH.exists(),
        "model_version": ml.VERSION_PATH.read_text().strip() if ml.VERSION_PATH.exists() else None,
    }


@app.post("/api/v1/train", response_model=TrainResponse)
def train(req: TrainRequest) -> TrainResponse:
    try:
        metrics, total = ml.train(req.lecturas, req.eventos, req.model_version)
    except ValueError:
        raise HTTPException(
            status_code=422,
            detail="Datos insuficientes para entrenar (se requieren al menos 30 ventanas de lecturas).",
        )
    return TrainResponse(model_version=req.model_version, activo=True, total_muestras=total, **metrics)


@app.post("/api/v1/predicciones", response_model=PredictResponse)
def predicciones(req: PredictRequest) -> PredictResponse:
    return ml.predict(req.paciente_id, req.lecturas)
