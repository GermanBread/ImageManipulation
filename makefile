all:
	make windows
	make linux
prepare:
	mkdir -p output
windows:
	make prepare
	dotnet publish -o build_win -r win-x64 -c RELEASE
	zip output/windows.zip build_win/*
linux:
	make prepare
	dotnet publish -o build_linux -r linux-x64 -c RELEASE
	zip output/linux.zip build_linux/*
