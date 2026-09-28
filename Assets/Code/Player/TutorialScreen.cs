using UnityEngine;

public class TutorialScreen : MonoBehaviour
{
    [SerializeField] private GameObject tutorialScreen;
    [SerializeField] private GameObject[] tutorialPages;
    [SerializeField] private GameObject playerUI;
    private int currentPage = 0;

    private void Awake()
    {
        ResolvePlayerUI();
    }

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
        ResolvePlayerUI();
    }
    public void nextButton()
    {
        // CHANGED: Fixed typo 'current page' -> 'currentPage'
        if (currentPage >= tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            tutorialScreen.SetActive(false);
            currentPage = 0;
            ResolvePlayerUI();
            if (playerUI != null)
            {
                playerUI.SetActive(true);
            }
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

    private void ResolvePlayerUI()
    {
        if (playerUI != null)
        {
            return;
        }

        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerController player in players)
        {
            if (player != null && (!player.IsSpawned || player.IsOwner) && player.PlayerHud != null)
            {
                playerUI = player.PlayerHud;
                return;
            }
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.tag == "playerUI")
            {
                playerUI = canvas.gameObject;
                return;
            }
        }
    }
}
