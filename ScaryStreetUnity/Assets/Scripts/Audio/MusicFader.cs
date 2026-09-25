using UnityEngine;

// Fades the new music track up and the old one out (used by SoundKit.PlayMusic).
public class MusicFader : MonoBehaviour
{
    AudioSource up, down;

    public void Begin(AudioSource fadeIn, AudioSource fadeOut) { up = fadeIn; down = fadeOut; }

    void Update()
    {
        float target = SoundKit.MusicVolume, dt = Time.unscaledDeltaTime;
        if (up) up.volume = Mathf.MoveTowards(up.volume, target, dt * 0.5f);
        if (down && down.isPlaying) { down.volume = Mathf.MoveTowards(down.volume, 0, dt * 0.7f); if (down.volume <= 0) down.Stop(); }
    }
}
