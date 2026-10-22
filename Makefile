SVG := About/realisticAxialTiltLogo.svg
JPG := About/Preview.jpg

.PHONY: build deploy release logs preview

build:
	dotnet build Source/RealisticAxialTilt.csproj -p:RimWorldVersion=1.6
	dotnet build Source/RealisticAxialTilt.csproj -p:RimWorldVersion=1.7
	uv run md_to_bbcode.py

logs:
	@mkdir -p cache
	cp "$(RIMWORLD_LOG)" "cache/$$(date +%Y%m%d_%H%M%S)_logs.txt"

preview:
	convert -density 96 $(SVG) -quality 99 $(JPG)


release:
	../release.sh Source/RealisticAxialTilt.csproj $(ARGS)

deploy:
	rsync -a --delete About/      $(RIMWORLD_MOD)/About/
	rsync -a --delete 1.6/        $(RIMWORLD_MOD)/1.6/
	rsync -a --delete 1.7/        $(RIMWORLD_MOD)/1.7/
	rsync -a --delete Languages/  $(RIMWORLD_MOD)/Languages/
	rsync -a --delete Source/     $(RIMWORLD_MOD)/Source/
	rsync -a --delete LICENSE     $(RIMWORLD_MOD)/LICENSE
