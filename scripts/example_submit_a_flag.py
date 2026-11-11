#!/usr/bin/env python3

FLAG_TO_SUBMIT = "META{n0t_a_r3al_fl4g}" # You'll need to replace this
#   with a real flag you got via exploiting another team's service.
# Make sure it's a live flag (not more than 10 ticks old).

import requests
import json
from pprint import pp

import configparser
config = configparser.ConfigParser()
config.read('ad_config.ini')

url = "https://api.ad.mctf.io/submit"
payload = { "flag_in": FLAG_TO_SUBMIT }
headers = { "team-token": config['metactf']['token'] }

response = requests.post(url, data=payload, headers=headers)
print(response.text)
