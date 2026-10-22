using TMPro;
using UnityEngine;

public class PlayerCurrencyUI : MonoBehaviour
{
    [SerializeField] private PlayerCurrency playerCurrency;
    [SerializeField] private TMP_Text amountText;

    private void Start()
    {
        if (playerCurrency == null)
        {
            playerCurrency = GetComponentInParent<PlayerCurrency>();
        }

        if (amountText == null)
        {
            Transform root = transform;
            PlayerController playerController = GetComponent<PlayerController>();
            if (playerController != null) root = playerController.transform;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.gameObject.name == "CurrencyGoldAmount")
                {
                    amountText = text;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (playerCurrency != null && amountText != null)
        {
            amountText.text = playerCurrency.Amount.ToString();
        }
    }
}
