using UnityEngine;

public class TutorialScreen : MonoBehaviour
{
    [SerializeField] private GameObject tutorialScreen;
    [SerializeField] private GameObject[] tutorialPages;
    [SerializeField] private GameObject playerUI;
    private int currentPage = 0;

    public void helpButton()
    {
        tutorialScreen.SetActive(true);
        tutorialPages[0].SetActive(true);
        tutorialPages[1].SetActive(false);
        tutorialPages[2].SetActive(false);
        tutorialPages[3].SetActive(false);
    }

    public void PressingNextsecondtime()
    {
        playerUI = GameObject.FindWithTag("playerUI");
    }
    public void nextButton()
    {
        // CHANGED: Fixed typo 'current page' -> 'currentPage'
        if (currentPage >= tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            tutorialScreen.SetActive(false);
            currentPage = 0;
            playerUI.SetActive(true);
        }
        else if (currentPage < tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            currentPage++;
            tutorialPages[currentPage].SetActive(true);
        }
    }
    //when tutorial pages are done turn player UI back on
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