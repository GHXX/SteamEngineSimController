# Steam Engine Simulator Controller

This is a C# program, that hooks into the Steam game [Steam Engine Simulator](https://store.steampowered.com/app/2381620/Steam_Engine_Simulator/), designed to work on Windows 10 **with the Power Generation DLC installed**. 

This project is not affiliated with the developer(s) of Steam Engine Simulator or the game itself in any official manner.

## What it does

Currently all this does is, determine location of some memory addresses and then read the *generator speed*, feed it to a PI(D) controller and compute a new value for the *reverser handle* and then set this value in the game.

I might extend it in the future, but it does work pretty well currently.

## How to use

You should be able to simply clone / download this repository, install the .NET 10 SDK, open up a shell and run the command `dotnet run` next to the `.csproj` file.

This should compile and start the program and then look for the game process. 
Once that is found it tries to discover the memory addresses (make sure you are either in the power generation sandbox or in the regular power generation mode, as the non-dlc mode will probably break).

Once all of this has happened, it will then control the reverser twice per second. It might be that the program errors every now and then, but it should automatically restart in such an event. If you change game mode I also recommend restarting the program.

## Contributing

Feel free to open up a *pull request* on Github if you would like to contribute, or fork it and develop your own project (see LICENSE).
