# MusicPlayer

## TO DO LIST

### Big stuff:

Right click on songs to pick them individually instead of full albums only

Queue management (dragging and dropping songs around to reorder, shuffle, clear)

When double clicking an album to queue it if you're opened on a different one it will move the row away stopping you from clicking twice on the album needs to only check if the first of the double clicks is on the album maybe?

Also allow double clicking of any song to play the full album (starting from the song being double clicked so just add all the songs to queue and then skip right to the selected one)

Backend data stuff (play counts and whatnot)

### Code housekeeping stuff

Split MainWindow.xaml.cs into a bunch of files it should just be a central hub for all the operations it shouldn't be doing the functions itself in a perfect world

### Bugs:

Small bug where on albums that have 3 columns (or more I'd assume) there is still a jiggle with the duration when you move the sidebar