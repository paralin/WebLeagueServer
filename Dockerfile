FROM mono:3.12.0-onbuild
CMD [ "mono",  "./WLNetwork.exe" ]
EXPOSE 4502
