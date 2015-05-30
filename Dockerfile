FROM mono:4.0.0-onbuild
CMD [ "mono",  "./WLNetwork.exe" ]
EXPOSE 8080 4502
