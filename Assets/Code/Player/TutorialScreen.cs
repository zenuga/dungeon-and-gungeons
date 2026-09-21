using UnityEngine;

public class TutorialScreen : MonoBehaviour
{
    [SerializeField] private GameObject tutorialScreen;
    [SerializeField] private GameObject BackButton;
    [SerializeField] private GameObject[] tutorialPages;
    private int currentPage = 0;

    public void helpButton()
    {
        tutorialScreen.SetActive(true);
        tutorialPages[0].SetActive(true);
        tutorialPages[1].SetActive(false);
        tutorialPages[2].SetActive(false);
        tutorialPages[3].SetActive(false);
    }

    public void nextButton()
    {
        // CHANGED: Fixed typo 'current page' -> 'currentPage'
        if (currentPage >= tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            tutorialScreen.SetActive(false);
            currentPage = 0;
        }
        else if (currentPage < tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            currentPage++;
            tutorialPages[currentPage].SetActive(true);
        }
    }

    public void backButton()
    {
        if (currentPage > 0)
        {
            tutorialPages[currentPage].SetActive(false);
            currentPage--;
            tutorialPages[currentPage].SetActive(true);
        }
    }
}