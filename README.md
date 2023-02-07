# DeezShade

![license badge](https://img.shields.io/badge/license-%20MIT--except--for--GShade--Developers-green)

Okay, now it's just funny. With love from NotNet and friends.

DeezShade is a continuation of [GeezShade](https://git.n2.pm/NotNite/geezshade), in C#.

**This is currently broken right now as Marot was banned from GitHub. Consider following [this guide](https://gist.github.com/ry00001/3e2e63b986cb0c673645ea42ffafcc26) or [temporarily patch out updates](https://notnite.com/gshade-patcher.html).**

[CLICK HERE TO INSTALL IT.](https://git.n2.pm/NotNite/DeezShade/releases/latest)

## Why?

GeezShade broke with the release of GShade 4.1.1, password protecting the assets behind a probably-not-legally-binding EULA as the password, stating that those who use it are accessing it through the GShade installer. This means they'd probably DMCA me if I included the password in my codebase, Base64'd or not.

So I came up with a solution. There is absolutely zero traces of the GShade password in this codebase.

## How?

They must've forgot that the GShade installer is written in C# and easily reflectable, allowing us to run the GShade installer code, within the GShade installer, without tampering it. Oops!
