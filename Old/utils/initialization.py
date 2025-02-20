import os

from dotenv import load_dotenv
from services.audio_generation_service import AudioGenerationService
from services.authentication_service import AuthenticationService

load_dotenv()
SECRET_KEY = os.getenv("SECRET_KEY")


def get_authentication_service() -> AuthenticationService:
    return AuthenticationService(SECRET_KEY)


def get_audio_generation_service() -> AudioGenerationService:
    return AudioGenerationService()
