import sqlite3

from fastapi import HTTPException, status, Depends
from fastapi.security import OAuth2PasswordBearer

from utils.hash_password import hash_password
from datetime import datetime, timedelta
from jose import jwt, JWTError

oauth2_scheme = OAuth2PasswordBearer(tokenUrl="token")


class AuthenticationService:
    def __init__(self, secret_key: str, db_path: str = 'users.db'):
        self.secret_key = secret_key
        self.db_path = db_path

    def verify_credentials(self, username: str, password: str) -> bool:
        hashed_password = hash_password(password, self.secret_key)
        connection = sqlite3.connect(self.db_path)
        cursor = connection.cursor()
        cursor.execute("SELECT Password FROM Users WHERE Username = ?", (username,))
        stored_password = cursor.fetchone()
        connection.close()

        return stored_password is not None and stored_password[0] == hashed_password

    def create_access_token(self, data: dict, expires_delta: timedelta = None):
        to_encode = data.copy()
        if expires_delta:
            expire = datetime.utcnow() + expires_delta
        else:
            expire = datetime.utcnow() + timedelta(minutes=15)
        to_encode.update({"exp": expire})
        encoded_jwt = jwt.encode(to_encode, self.secret_key, algorithm="HS256")
        return encoded_jwt

    def get_current_user(self, token: str = Depends(oauth2_scheme)):
        credentials_exception = HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Could not validate credentials",
            headers={"WWW-Authenticate": "Bearer"},
        )
        try:
            payload = jwt.decode(token, self.secret_key, algorithms=["HS256"])
            username: str = payload.get("sub")
            if username is None:
                raise credentials_exception
            return username
        except JWTError:
            raise credentials_exception
