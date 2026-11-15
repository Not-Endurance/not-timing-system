namespace NC.Server.Authentication.Interface;

/// <summary>
/// The app's rules for its profile. The module asks it when a profile is registered or changed. The app decides what a
/// valid profile is: the module requires only the names of the user.
/// </summary>
public interface IProfileValidator<in TProfile>
{
    bool Accepts(TProfile profile);
}
