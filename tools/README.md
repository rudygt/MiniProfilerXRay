# MiniProfilerXRay

## running a local xray agent using docker

* build docker image

run this command in the xraylocal folder

```
docker build -t xray-daemon .
```

* run local xray daemon (you will neeed an aws key/secret)

```
docker run --rm --attach STDOUT -e AWS_ACCESS_KEY_ID=[put your key id here] -e AWS_SECRET_ACCESS_KEY=[put your key secret here] -e AWS_REGION=us-east-1 --name xray-daemon -p 2000:2000/udp xray-daemon -o
```

* optionally you can run this container instead of the local xray agent to inspect udp packets whitout sending them to the xray service

```
docker run --rm -it --privileged --name=tcpdump -p 2000:2000/udp itsthenetwork/alpine-tcpdump -nn -A 'udp and dst port 2000'
```

after setting up the proper container, you can start any of the samples passing the endpoint of your docker container to the XRayMiniprofilerStorage

the default used in the test projects is "192.168.99.100:2000"