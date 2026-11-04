using System.Linq;

namespace AerolineaVIP.Models
{
    public static class IdentityDocumentRules
    {
        public static bool EsCedulaEcuatorianaValida(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero) || numero.Length != 10 || numero.Any(c => c < '0' || c > '9'))
            {
                return false;
            }

            int provincia = (numero[0] - '0') * 10 + numero[1] - '0';
            int tercerDigito = numero[2] - '0';
            if (provincia < 1 || provincia > 24 || tercerDigito > 5)
            {
                return false;
            }

            int suma = 0;
            for (int i = 0; i < 9; i++)
            {
                int digito = numero[i] - '0';
                if (i % 2 == 0)
                {
                    digito *= 2;
                    if (digito > 9) digito -= 9;
                }
                suma += digito;
            }

            int verificador = (10 - suma % 10) % 10;
            return verificador == numero[9] - '0';
        }
    }
}
