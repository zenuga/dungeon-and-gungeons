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
            amountText = GetComponentInChildren<TMP_Text>(true);
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