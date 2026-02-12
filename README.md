# save-the-cats unity
## Enabling communications
Refer to [here](https://github.com/MonkeScripts/save-the-cats/tree/main/zenoh#generating-ssltls-certificates) on how to generate the relevant certificates.

Add the server side certificate into `Assets/Certificates` and then run your Unity game. 

So far: 
1. Pressing space would publish a `heeheehorhor` msg
2. The program subscribes to `ultra/action1` for data from the ultra96

Thank you mukund

## Issues
As a new person doing Unity, I do not know what to gitignore. So far I have just been following [this](https://stackoverflow.com/questions/59742994/should-i-push-the-unity-library-folder-to-github). Please tell me if I miss out anything :(