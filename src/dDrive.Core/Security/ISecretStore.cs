using System.Security;

namespace dDrive.Core.Security;

public interface ISecretStore
{
    void Save(string target, SecureString secret);
    SecureString? Read(string target);
    void Delete(string target);
}