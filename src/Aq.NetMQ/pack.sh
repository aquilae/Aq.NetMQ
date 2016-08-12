#!/usr/bin/env bash

# http://stackoverflow.com/questions/59895/can-a-bash-script-tell-which-directory-it-is-stored-in
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
	CONFIGURATION=Release
fi

BINPATH=`realpath --relative-to . $DIR/bin`
NUGET=`cygpath -u $NUGET_LOCAL`

if [ "$(ls -A $BINPATH/$CONFIGURATION/*.nupkg 2>/dev/null)" ]; then
	printf "\033[0;35mRemoving old package versions\033[0m\n"
	rm -v $BINPATH/$CONFIGURATION/*.nupkg
	echo
fi

printf "\033[0;35mBuilding and packing\033[0m\n"
dotnet pack -c "$CONFIGURATION"
echo

printf "\033[0;35mPushing to local NuGet\033[0m\n"
cp -fv -t $NUGET $BINPATH/$CONFIGURATION/*.nupkg
