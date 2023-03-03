# DeezShade

![license badge](https://img.shields.io/badge/license-%20MIT--except--for--GShade--Developers-green)

Okay, now it's just funny. With love from NotNet and friends.

DeezShade is a continuation of [GeezShade](https://git.n2.pm/NotNite/geezshade), in C#. It allows you to download GShade shaders and presets while using them with ReShade.

Warning: This might break or be dangerous, and I don't always have time to fix it. Marot can and has done arbitrary things through DeezShade like performing a PC shutdown. Consider following [this guide](https://gist.github.com/ry00001/3e2e63b986cb0c673645ea42ffafcc26) for manual installation steps.

[CLICK HERE TO INSTALL IT.](https://git.n2.pm/NotNite/DeezShade/releases/latest)

## Why?

GeezShade broke with the release of GShade 4.1.1, password protecting the assets behind a probably-not-legally-binding EULA as the password, stating that those who use it are accessing it through the GShade installer. This means they'd probably DMCA me if I included the password in my codebase, Base64'd or not.

So I came up with a solution. There is absolutely zero traces of the GShade password in this codebase.

## How?

They must've forgot that the GShade installer is written in C# and easily reflectable, allowing us to run the GShade installer code, within the GShade installer, without tampering it. Oops!
