all: build

clean:
	xbuild /t:Clean ./WLNetwork.sln

submodule:
	git submodule update --init

build: submodule
	xbuild /p:Configuration=Release ./WLNetwork.sln
