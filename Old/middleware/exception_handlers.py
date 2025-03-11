from fastapi import FastAPI, Request, HTTPException
from starlette.responses import JSONResponse


def add_exception_handlers(app: FastAPI):
    @app.exception_handler(HTTPException)
    async def http_exception_handler(request: Request, exc: HTTPException):
        # Returns a JSON response with a 401 status code and a specific error message.
        message = "Exception occurred"
        if exc.status_code == 401:
            message = "Credenciales incorrectas"
        return JSONResponse(
            status_code=exc.status_code,
            headers={"WWW-Authenticate": "Basic"},
            content={"message": message}
        )
