namespace forzion.tech.Application.Interfaces;

public interface IRecipientHasher
{
    string HashEmail(string email);

    string HashTelefone(string telefone);
}
