
secret?=tagada

build: clean
	mkdir dist
	echo $(secret) > dist/secret.txt

publish:
	mkdir dist/publish
	cp dist/secret.txt dist/publish/secret-published.txt

clean:
	-rm -rf dist
