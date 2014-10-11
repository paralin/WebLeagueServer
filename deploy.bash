#!/bin/bash
xbuild /p:Configuration=Release ./WLNetwork/WLNetwork.csproj
rsync -rav --delete ./WLNetwork/bin/Release/ sg@sg:~/wlnetwork/

xbuild /p:Configuration=Release ./WLBot/WLBot.csproj
rsync -rav --delete ./WLBot/bin/Release/ sg@sg:~/wlbot/
