using UnityEngine;

public class TutorialScreen : MonoBehaviour
{
    public static bool IsBlockingHud { get; private set; }
    [SerializeField] private GameObject tutorialScreen;
    [SerializeField] private GameObject[] tutorialPages;
    [SerializeField] private GameObject playerUI;
    private int currentPage = 0;

    private void Awake()
    {
        if (tutorialScreen != null)
        {
            tutorialScreen.SetActive(true);
            IsBlockingHud = true;
            currentPage = 0;
            for (int i = 0; tutorialPages != null && i < tutorialPages.Length; i++)
            {
                if (tutorialPages[i] != null) tutorialPages[i].SetActive(i == 0);
            }
        }
        else
        {
            IsBlockingHud = false;
        }

        ResolvePlayerUI();
        if (playerUI != null) playerUI.SetActive(false);
    }

    public void helpButton()
    {
        tutorialScreen.SetActive(true);
        IsBlockingHud = true;
        if (playerUI != null) playerUI.SetActive(false);
        currentPage = 0;
        for (int i = 0; i < tutorialPages.Length; i++)
        {
            if (tutorialPages[i] != null) tutorialPages[i].SetActive(i == 0);
        }
    }

    public void PressingNextsecondtime()
    {
        ResolvePlayerUI();
    }
    public void nextButton()
    {
        if (tutorialScreen == null || tutorialPages == null || tutorialPages.Length == 0)
        {
            IsBlockingHud = false;
            ResolvePlayerUI();
            if (playerUI != null) playerUI.SetActive(true);
            return;
        }

        if (currentPage >= tutorialPages.Length - 1)
        {
            tutorialPages[currentPage].SetActive(false);
            tutorialScreen.SetActive(false);
            IsBlockingHud = false;
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
