FROM mono:4.0.0

MAINTAINER Christian Stewart <kidovate@gmail.com>

RUN mkdir -p /usr/src/app/source /usr/src/app/build
WORKDIR /usr/src/app/source

COPY . /usr/src/app/source
RUN nuget restore -NonInteractive
#RUN xbuild /property:Configuration=Release /property:OutDir=/usr/src/app/build/
RUN xbuild /property:Configuration=Debug /property:OutDir=/usr/src/app/build/
WORKDIR /usr/src/app/build
RUN rm -rf /usr/src/app/source

CMD [ "mono",  "./WLNetwork.exe" ]
EXPOSE 8080 4502
