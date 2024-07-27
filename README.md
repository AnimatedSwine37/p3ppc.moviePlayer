# p3ppc.moviePlayer

A library for Persona 3 Portable PC (Steam) that lets other mods play movies through flowscript.

## Usage (Users)
When a movie is playing you can press the Start button (or `X` on keyboard) to skip the movie. As to installing this, if you are using mods that utilise this you shouldn't need to do anything, Reloaded should automatically download this.

## Usage (Makers)
Note that this assumes you are editing files using BF Emulator. For information on that check the [Persona Essentials docs](https://sewer56.dev/p5rpc.modloader/usage/#editing-bf-files) and [BF Emulator docs](https://sewer56.dev/FileEmulationFramework/emulators/bf.html).

There are a few things you need to do to make mods that utilise this:
1. Make Movie Player a dependency of your mod in Reloaded: to do this you can right-click and edit your mod, from there go to the dependencies tab and enable Movie Player (if you don't see it you need to download it individually first, don't worry users will not need to do this).
2. Add a file called `Functions.json` to your `FEmulator/BF` folder. Copy the following into it:
```json
[
  {
    "Index": "0x0004",
    "ReturnType": "void",
    "Name": "CUSTOM_MOVIE_PLAY",
    "Description": "Custom function that plays a usm based on its id. This REQUIRES the Movie Player mod to work!",
    "Parameters": [
      {
        "Type": "int",
        "Name": "CutsceneId",
        "Description": "The id of the usm to play, the usm should be in \\data\\sound\\usm and be called CutsceneId.usm (e.g. 21.usm for id 21)"
      }
    ]
  }
]
```

3. Put a usm file you want to play in the `data\sound\usm` folder (in `umd1.cpk`) and name it `x.usm` where x is some integer e.g. `69.usm`. For information on how to create a usm to use you can look at the [Amicitia USM wiki page](https://amicitia.miraheze.org/wiki/USM)
4. In any flowscript call your usm using `CUSTOM_MOVIE_PLAY(x);`, this will play the video until it ends or the user skips it. Note that this will not stop bgm or anything like that so you may need to use some additional functions before and after playing the movie to make it all nice.
