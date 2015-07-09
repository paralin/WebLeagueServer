FROM debian:wheezy
MAINTAINER Christian Stewart <kidovate@gmail.com>

RUN apt-get update \
  && apt-get install -y curl \
  && rm -rf /var/lib/apt/lists/*

RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF

RUN echo "deb http://download.mono-project.com/repo/debian wheezy/snapshots/4.0.0 main" | tee /etc/apt/sources.list.d/mono-xamarin.list \
  && apt-get update \
  && apt-get install -y mono-devel ca-certificates-mono nuget \
  && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /usr/src/app/source /usr/src/app/build
WORKDIR /usr/src/app/source

COPY . /usr/src/app/source
RUN nuget restore -NonInteractive
RUN xbuild /property:Configuration=Release /property:PlatformTarget=x86 /property:Platform=x86 /property:OutDir=/usr/src/app/build/
WORKDIR /usr/src/app/build
RUN cp /usr/src/app/source/start.bash /usr/src/app/build/ && rm -rf /usr/src/app/source

CMD bash start.bash
EXPOSE 8080 4502
