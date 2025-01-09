namespace BoatControl.Shared.Api
{
    public class SendPinArgs
    {
        public string Email { get; set; }
    }
    public class VerifyPinArgs
    {
        public string Email { get; set; }

        public string Pin { get; set; }
    }

    public class ResetPasswordByPinArgs
    {
        public string Email { get; set; }

        public string Pin { get; set; }

        public string NewPassword { get; set; }

    }


    public class ChangePasswordArgs
    {
        public string Email { get; set; }

        public string CurrentPassword { get; set; }

        public string NewPassword { get; set; }

    }

}