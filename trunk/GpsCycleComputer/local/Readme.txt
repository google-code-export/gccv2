Instructions to create your own voice in your language

For each language or voice create a directory, example "eng" for english or "eng_Lisa".
This directory holds the .wav files and so called sequence files.
The voice navigation sentences are defined in sequence files,
because in different languages the order of words can be different.
In the sequence files there are *.wav files referenced which will be played in sequence.
In addition there are some variable words (%xxx) which will be replaced by GCC according navigation situation.

GCC supports 4 sequences with the following variable words:

- Seq_toRoute.txt
  %Distance:   [number].wav
  %Unit:       Kilometers.wav | Kilometer.wav | Meters.wav
  %Direction:  [number].wav   or   North.wav | East.wav | South.wav | West.wav
  %OClock:     OClock.wav   or   nothing
  Example: Drive to route fivehundred meters in direction two o'clock.

- Seq_turn.txt
  %In:         In.wav   or   nothing
  %Distance:   [number].wav   or   Now.wav
  %Unit:       Meters.wav   or   nothing
  %Half:       Half.wav | Sharp.wav   or   nothing
  %Left:       Left.wav | Right.wav
  Example: In twohundred meters turn half left.

- Seq_destination.txt
  %In:         In.wav   or   nothing
  %Distance:   [number].wav   or   Now.wav
  %Unit:       Meters.wav   or   nothing
  Example: In twohundred meters you have reached your destination.

- Seq_test.txt
               only fixed words (whatever you like)


needed *.wav files (for variable words):

Many.wav
Twelve.wav
Ten.wav
Eight.wav
Six.wav
Four.wav
Two.wav
One.wav
Fivehundred.wav
Twohundred.wav
Onehundred.wav
Now.wav
Kilometers.wav
Kilometer.wav
Meters.wav
Half.wav
Sharp.wav
Left.wav
Right.wav
OClock.wav
North.wav
East.wav
South.wav
West.wav




further used *.wav files (you can define and reference .wav files whatever you want):

In.wav
Turn.wav
InDirection.wav
YouHaveReachedYourDestination.wav
DriveToRoute.wav



Tips:
- You can create your own .wav files with your Mobile with "Notes" (in Menu enable "View Recording Toolbar").
- Format: 8000Hz, 8-bit, MONO
- When recording, always speak the whole sentence and cut out the words or phrases later in an audio editor.
  Otherwise it will sound later clipped and artificial.

