FROM debian:wheezy
MAINTAINER Christian Stewart <christian@paral.in>

RUN apt-key adv --keyserver pgp.mit.edu --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
RUN echo "deb http://download.mono-project.com/repo/debian wheezy main" > /etc/apt/sources.list.d/mono-xamarin.list \
	&& apt-get update \
	&& apt-get install -y mono-devel ca-certificates-mono fsharp mono-vbnc nuget curl \
	&& rm -rf /var/lib/apt/lists/*

RUN mkdir -p /usr/src/app/source /usr/src/app/build
WORKDIR /usr/src/app/source

COPY . /usr/src/app/source
RUN nuget restore -NonInteractive
RUN xbuild /property:Configuration=Release /property:Platform=Mixed\ Platforms /property:OutDir=/usr/src/app/build/

WORKDIR /usr/src/app/build
RUN cp /usr/src/app/source/start.bash /usr/src/app/build/ && rm -rf /usr/src/app/source

CMD bash start.bash
EXPOSE 8080 4502
