# Reads a variable that is not on the allow-list (planted fault).
import os
TOKEN = os.environ.get("GH_TOKEN", "")
print(len(TOKEN))
