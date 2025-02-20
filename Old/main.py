import aiofiles
import uvicorn

from fastapi import FastAPI, Depends, HTTPException, status
from fastapi.security import HTTPBasic, HTTPBasicCredentials
from datetime import timedelta

from starlette.responses import Response

from middleware.exception_handlers import add_exception_handlers
from models.generate_audio_request import GenerateAudioRequest
from services.audio_generation_service import AudioGenerationService
from services.authentication_service import AuthenticationService
from utils.initialization import get_authentication_service, get_audio_generation_service

app = FastAPI()
add_exception_handlers(app)
security = HTTPBasic()


@app.post("/login")
def login(
        credentials: HTTPBasicCredentials = Depends(security),
        auth_service: AuthenticationService = Depends(get_authentication_service)):
    if not auth_service.verify_credentials(credentials.username, credentials.password):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED)

    access_token = auth_service.create_access_token(
        data={"sub": credentials.username}, expires_delta=timedelta(hours=48)
    )
    return {"access_token": access_token, "token_type": "bearer"}


@app.post("/generate")
async def generate_audio(
        request: GenerateAudioRequest,
        audio_service: AudioGenerationService = Depends(get_audio_generation_service),
        current_user: str = Depends(get_authentication_service().get_current_user)):
    audio_path = audio_service.generate_audio(request.text, request.voice, request.audio_format)

    async with aiofiles.open(audio_path, 'rb') as audio_file:
        audio_data = await audio_file.read()

    content_type = "audio/mpeg" if request.audio_format == "mp3" else "audio/aac"
    return Response(content=audio_data, media_type=content_type)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8080)
