import hashlib


def hash_password(password: str, secret_key: str) -> str:
    return hashlib.sha256((password + secret_key).encode()).hexdigest()
