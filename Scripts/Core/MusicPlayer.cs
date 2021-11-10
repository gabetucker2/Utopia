using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public AudioSource musicPlayer;
    [Range(0f, 500f)] public float secondsBetweenSongs;
    [Range(0f, 500f)] public float secondsBetweenSongsMaxVariation;
    public List<AudioClip> musicList;

    private bool debounce = false;

    IEnumerator PlayNewSong()
    {
        int thisRoll = Mathf.CeilToInt(Random.Range(0f, musicList.Count - 1f) - 1f);
        musicPlayer.clip = musicList[thisRoll];

        musicPlayer.Play();

        yield return new WaitForSeconds(secondsBetweenSongs + Random.Range(0f, secondsBetweenSongsMaxVariation) + musicList[thisRoll].length);

        debounce = false;
    }

    private void Update()
    {
        if (!musicPlayer.isPlaying && !debounce)
        {
            debounce = true;

            StartCoroutine(PlayNewSong());
        }
    }
}
