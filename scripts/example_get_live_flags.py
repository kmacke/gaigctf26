#!/usr/bin/env python3

import requests
import json
from pprint import pp

import configparser
config = configparser.ConfigParser()
config.read('ad_config.ini')

url = "https://api.ad.mctf.io/live_flags"
headers = {"team-token": config['metactf']['token']}

response = requests.get(url, headers=headers)
result = json.loads(response.text)

pp(result)
