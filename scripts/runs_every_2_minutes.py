#!/usr/bin/env python3

import requests
import json

import configparser
config = configparser.ConfigParser()
config.read('ad_config.ini')
teamnum = config['metactf']['teamnum']
print(f"Team {teamnum} example periodic script is running!")

################################################
### BELOW THIS LINE, WRITE YOUR ATTACK CODE! ###
################################################



################################################
###     If you'd like some suggestions:      ###
################################################

### This is how you could connect to the scoreboard and get the list of all live flags:
# url = "https://api.ad.mctf.io/live_flags"
# headers = {"team-token": config['metactf']['token']}
# response = requests.get(url, headers=headers)
# live_flags = json.loads(response.text)
### You could use that live flag data to target your attacks!

### This is how you could connect to Team 1's vulnerable service:
# url = "http://service1.ad.mctf.io:5001/"
# response = requests.get(url)
# result = response.text
### Instead of just GETting a URL of the vulnerable service, you'll probably need to pull off some kind
###  of attack, such as a buffer overflow, directory traversal, SQL injection, etc.

### The typical attack script might look like this:
###   1) Via live_flags API, get list of all the live flags.
###   2) Use one or more exploits against all teams' service endpoints (except yours) to steal
###       the flags referred to by the live flags' identifiers.
###   3) Submit any flags you've successfully stolen to the scoreboard via the /submit API.
### Good luck!
