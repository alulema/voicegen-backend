import sqlite3
import os


def create_database():
    db_exists = os.path.exists('../users.db')

    if not db_exists:
        connection = sqlite3.connect('../users.db')
        cursor = connection.cursor()
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY,
                Username TEXT NOT NULL UNIQUE,
                Password TEXT NOT NULL
            )
        ''')
        connection.commit()
        connection.close()
        print("Base de datos creada y configurada.")
    else:
        print("La base de datos ya existe.")


if __name__ == '__main__':
    create_database()
