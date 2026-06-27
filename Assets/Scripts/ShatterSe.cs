using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class ShatterSe : MonoBehaviour
{
    private AudioSource audioSource;

    void Awake()
    {
        // 自分にくっついている AudioSource を取得。なければ追加する
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 自分にくっついている Button の OnClick に自動登録する
        Button button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("ShatterSe: Button コンポーネントが見つかりません。", this);
            return;
        }
        button.onClick.AddListener(Play);
    }

    public void Play()
    {
        audioSource.Play();
    }
}
