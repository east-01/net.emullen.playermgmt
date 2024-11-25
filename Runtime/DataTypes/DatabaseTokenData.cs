namespace EMullen.PlayerMgmt 
{

    [RequireVisibilityHandler(Handler.OWNER_AND_SERVER)]
    public class DatabaseTokenData : PlayerDataClass 
    {
        public readonly string token;

        public DatabaseTokenData(string token) 
        {
            this.token = token;
        }
    }
}