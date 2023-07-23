Music Table
======

This is a sample music sequencer project using Unity3D.

A sample screen with 15 pads represented 15 different sounds. You can use the default drum sounds, or record your own into each pad

![demo](Demos/music%20table.gif)


The main mechanism for timing is the repeated checking of `AudioSettings.dspTime` in the `Update()` method of the MonoBehaviour singleton `PlaybackManager.cs` against the time of the next known "beat", which is a function of the current tempo.  When the current time is within a given convergence criterion (in this case, 50ms), the AudioSource for any new notes to be played are then scheduled at the time of the impending beat, and the next beat time is incremented.



