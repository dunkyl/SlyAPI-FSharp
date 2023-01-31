namespace net.dunkyl.SlyAPI

open System
open System.Threading.Tasks

/// Implementers manager exhanging keys and creating redirects to turn app files and grants into
/// user tokens.
/// Step 2 is not a method here since it occurs between the user and the 3rd party service.
type OAuthWizard<'User> =
    /// <summary>
    /// OAuth 2 step 1 is to generate a URL to redirect the user to.
    /// </summary>
    /// <param name="state">Any string up to 74 characters long. Returned to you 
    ///  in step 3 after rediret.</param>
    /// <returns>The full URL to redirect the user to for step 2</returns>
    abstract member Step1: state: string -> redirect: Uri -> scopes: string Set -> Uri

    /// OAuth 2 step 3 is to exchange the code given by the redirected user for their access token.
    /// TODO: return state
    abstract member Step3: state: string -> code: string -> Task<'User>