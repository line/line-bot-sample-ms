namespace App.Linebot
{
    /// <summary>
    /// Linebot のコンフィグオプション。
    /// </summary>
    public class LinebotOptions
    {
        /// <summary>
        /// 作業用ファイルの内部保存領域のルート
        /// </summary>
        public string WorkingFileStoreRoot { get; set; }

        /// <summary>
        /// 生成されたファイルの内部保存領域のルート
        /// </summary>
        public string GeneratedFileStoreRoot { get; set; }

        /// <summary>
        /// 生成されたファイルの公開Urlのルート
        /// </summary>
        public string GeneratedFilePublicUrlRoot { get; set; }

        /// <summary>
        /// 生成されたファイルの公開Urlのパス
        /// </summary>
        public string GeneratedFilePublicUrlPath { get; set; }
    }
}
