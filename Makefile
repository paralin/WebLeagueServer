all: build

clean:
	xbuild /p:Configuration=Clean ./WLNetwork.sln

submodule:
	git submodule update --init

build: submodule
	xbuild /p:Configuration=Release ./WLNetwork.sln

deploy: build
	rsync -rav --delete ./WLNetwork/bin/Release/ sg@sg:~/wlnetwork/
	rsync -rav --delete ./WLBot/bin/Release/ sg@sg:~/wlbot/
