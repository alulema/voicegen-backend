from pydantic import BaseModel


class GenerateAudioRequest(BaseModel):
    voice: str
    audio_format: str
    text: str
