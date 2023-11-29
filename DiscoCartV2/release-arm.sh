#!/bin/bash

set -e

if [ -n "$1" ]; then
  export sub=1
fi

rm -rf bin/Release/net7.0
rm -rf bin/Release/DiscoCartUtil-Linux
rm -rf DiscoCartUtil-Linux.tar.gz

dotnet publish DiscoCartUtil/DiscoCartUtil.csproj /p:Configuration=Release /p:Platform="Any CPU" /p:OutputPath=../bin/Release/net7.0/ -r linux-arm64 --self-contained

cd bin/Release
mkdir DiscoCartUtil-Linux
mkdir DiscoCartUtil-Linux/bin
cp -r net7.0/publish/* DiscoCartUtil-Linux/bin

cp ../../../LICENSE DiscoCartUtil-Linux
mv DiscoCartUtil-Linux/bin/DiscoCartUtil DiscoCartUtil-Linux/bin/discocartutil

tar -zcvf ../../DiscoCartUtil-Linux-arm64.tar.gz DiscoCartUtil-Linux
if [ -d "/mnt/src/upload" ]
then
  cp ../../DiscoCartUtil-Linux-arm64.tar.gz /mnt/src/upload  
fi
cd ../..

exit 0;

