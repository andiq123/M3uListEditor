Instruction:
This app scans your m3u file, and creates a new one besides him with only working links, the non working ones will be ignored.

Info: This app isn't perfect, it may vary file to file, but you can change the TimeOut by adding an arg "-timeOut 5" 5 stands for seconds.
      Default TimeOut is 10

1. open a terminal inside the project
2. type dotnet run
3. drag and drop the m3u file, or paste a link from internet
4. wait to end the process
4. beside the original file should appear a new file with the same name and the prefix -cleaned
5. enjoy
6. New Feature, app removes automatically if finds doubles, you can deactivate it, use argument: -rd false OR -removeDoubles false
___

Or you can add arguments like -src and -dest, to set the source path and destination manually, or to be scripted, set the timeout -timeOut 5;

Example of putting link: dotnet run --src link --dest "C:\Users\{username}\Desktop\test.m3u";
if you dont give a destination to a link, it will be created in the porject folder a file "TempFile" And "TempFile-cleaned"  


Linux use:
1. Download the pre release winrar
2. Unzip, and inside the folder
1. Open a terminal
2. chmod +x UI
3. ./UI
5. example of use with arugments ./UI -src [sourcePath Or Link] -dst [DestinationPath]
6. or you can just type ./UI and press enter, follow instructions
