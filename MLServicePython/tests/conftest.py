import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import pytest  # noqa: E402

from app import ml  # noqa: E402


@pytest.fixture(autouse=True)
def _aislar_modelo(tmp_path, monkeypatch):
    """Redirige el modelo a un directorio temporal para no ensuciar model_data/.

    El test de entrenamiento persiste model.pkl y version.txt; sin este aislamiento
    escribiría sobre el model_data/ real del repositorio.
    """
    directorio = tmp_path / "model_data"
    directorio.mkdir(exist_ok=True)
    monkeypatch.setattr(ml, "MODEL_DIR", directorio)
    monkeypatch.setattr(ml, "MODEL_PATH", directorio / "model.pkl")
    monkeypatch.setattr(ml, "VERSION_PATH", directorio / "version.txt")
