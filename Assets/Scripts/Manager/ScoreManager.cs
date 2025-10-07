using System;
using System.Globalization;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager i;

    [Header("현재 점수")]
    public int nowScore = 0;
    public TextMeshProUGUI nowScoreText;

    [Header("최고 점수")]
    public int highScore = 0;
    public TextMeshProUGUI highScoreText;

    private const string HighScoreKey = "KEY_HighScore";

    private void Awake()
    {
        i = this;
        LoadHighScore();
        RenewScoreText();
        RenewHighScoreText();
    }

    public void RenewScoreText()
    {
        if (nowScoreText != null)
            nowScoreText.text = nowScore.ToString("N0", CultureInfo.InvariantCulture);
    }

    public void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        RenewHighScoreText();
    }

    public void SaveHighScore()
    {
        PlayerPrefs.SetInt(HighScoreKey, highScore);
        PlayerPrefs.Save();
    }

    public void AddNowScore()
    {
        AddNowScore(1);
    }

    public void AddNowScore(int amount)
    {
        nowScore += amount;
        if (nowScore < 0) nowScore = 0;
        RenewScoreText();
    }

    public void CompareNowScore()
    {
        if (nowScore > highScore)
        {
            highScore = nowScore;
            SaveHighScore();
            RenewHighScoreText();
        }
    }

    private void RenewHighScoreText()
    {
        if (highScoreText != null)
            highScoreText.text = highScore.ToString("N0", CultureInfo.InvariantCulture);
    }
}