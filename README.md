
# C# Unity SDK

This SDK wraps the Feed.fm REST API for the Unity environment.
The SDK uses only Unity APIs and no native extensions (as opposed
to our earlier [Unity for iPhone SDK](https://github.com/fuzz-radio/Unity-SDK)), 
so it works well in all Unity environments, including the editor.

## Sample Scene

The fastest way to understand how the SDK works is to install the package and
open the sample scene in Assets/FeedFm Demos/PlayerScene.unity.

When you run the sample scene in the editor, you'll see a spinning cube, and
overlaid on top of it is a GUI menu that lets you play music and select music
stations. If you click 'play' or one of the station buttons, you should
immediately start hearing music. You can play/pause the music or change stations
again.

From within the Unity editor, you can see that the sample scene consists of a
cube, a directional light, and the main camera. All of the code related to the
Feed.fm music player is attached to the Main Camera object. Specifically the
'Player' script and the 'Player GUI' script are what power the Feed.fm music in
this scene.

The 'Player' script is what handles all the logic and communication with the
Feed.fm service. This script requests music from Feed.fm and creates an AudioSource
to play the music. The script exposes a simple API for starting and stopping music,
and it exposes some simple C# events that keep you informed about what the player is
doing. The code for the player is in Assets/Plugins/FeedFm/Player.cs.

The only configuration needed by the Player script is the 'token' and 'secret'
given to you by Feed.fm when you register and create music stations. This
token/secret pair identifies you and the music you are offering to your users -
the default values in this script are useful only for testing.

The 'Player GUI' script is an example of how you can interact with the Player
script and expose controls to game players.

## Player GUI

There are really only two parts to the Player GUI script: initializing the
Player and rendering the controls.

### Initialization

Player initialization is quite simple. First, in Awake(), we retrieve a reference
to the player. Then, in Start(), we attach some event handlers and call player.Tune().

The events we listen to are changes to the list of stations the server gives us.
When the list is changed, the names of the stations are extracted into an array
of strings for use by GUILayout.SelectionGrid when we're rending the station list.

The call to player.Tune() is what kicks off communication with the server. The
player asynchronously sends credentials to the server, retrieves a list of
available stations, and queues up the first available song for playback. If
we wanted music to play as soon as the scene started, we could have called
player.Play() instead.

### Rendering

Rendering the player and the player controls is largely based on inspecting
the current state of the player by looking at the player.currentState
property. This is an enum type called PlayerState, and it has 5 values that
describe the state that the player is in:

* Idle - This is the default state of the player when no music is playing or
expected to be playing.
* Tuning - This is the state after a call to Play() is made, but before
music is has begun playing.
* Playing - This is when music is actively being played.
* Paused - This is when music that had been playing has been paused.
* Exhausted - If there is no music available in the current station, this
becomes the player's state. Changing the station id or calling Tune() or Play()
will cause the player to try to retrieve more music and get back to the Idle
or Play state.

When rendering the current state, the player exposes the current station we're
tuned to in the player.station property and the current song that is being
played (if any) in the player.activePlay property. Both properties are JSONNode
instances, which come from the SimpleJSON library. The data contained in them
come from the responses to the Feed.fm REST API, so the activePlay value maps
to the "play" value in the response to POST /play, and the station value maps
to an item in the "stations" value in the response to GET /placement/:id.

## Caveats

Due to licensing constraints, music playback can only occur within the
United States. If our server geolocates the client to be outside of the
US, then music will not be delivered to the clent and an 'onNotInUS' event
will be triggered from the player instance.

This API retrieves and plays one song at a time from the Internet, so
the client must have an active Internet connection for music playback
to happen.

The sample scene included in this package will freely play a station
created by Feed.fm. To create and play your own stations, you will need
to register with Feed.fm and pay a subscription fee to cover
payments to artists and labels. Rates are around $0.0016 per minute
of playback. Subscriptions start at $25/month. Everything can
be completed without negotiation or talking to a salesperson.


