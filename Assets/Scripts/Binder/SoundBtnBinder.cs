using UnityEngine;

public class SoundBtnBinder : MonoBehaviour
{
    public SoundSet soundSet;
    
    public void ToggleSoundBtn()
    {
        SoundManager.i.ToggleAudioSettings(soundSet);
    }
}
