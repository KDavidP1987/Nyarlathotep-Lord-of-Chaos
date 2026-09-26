# Reads a token through an imported environment object (planted fault).
from os import environ
TOKEN = environ.get("GH_TOKEN", "")
print(len(TOKEN))
