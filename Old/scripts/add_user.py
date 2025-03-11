import hashlib
import os
import sqlite3
import getpass
from pathlib import Path

from dotenv import load_dotenv

script_dir = Path(__file__).parent.absolute()
env_path = script_dir.parent / '.env'
load_dotenv(env_path)
SECRET_KEY = os.getenv("SECRET_KEY")


def add_user(username: str, password: str):
    hashed_password = hashlib.sha256((password + SECRET_KEY).encode()).hexdigest()
    connection = sqlite3.connect(script_dir.parent / 'users.db')
    cursor = connection.cursor()
    cursor.execute("INSERT INTO Users (Username, Password) VALUES (?, ?)", (username, hashed_password))
    connection.commit()
    connection.close()


if __name__ == '__main__':
    username = input("Enter username: ")

    while True:
        password = getpass.getpass("Enter password: ")
        confirm_password = getpass.getpass("Confirm password: ")

        if password == confirm_password:
            break
        else:
            print("Passwords do not match. Please try again.")

    add_user(username, password)
    print("User added successfully.")
