﻿/*==================================
             ########
            ##########

             ########
            ##########
          ##############
         #######  #######
        ######      ######
        #####        #####
        ####          ####
        ####   ####   ####
        #####  ####  #####
         ################
          ##############
==================================*/

namespace Business.Auth
{
    public interface IToken
    {
        string Key { get; set; }

        string Remote { get; set; }

        string CommandID { get; set; }
    }

    [ProtoBuf.ProtoContract(SkipConstructor = true)]
    public class Token : IToken
    {
        public static implicit operator Token(string value)
        {
            return Business.Extensions.Help.JsonDeserialize<Token>(value);
        }

        public static implicit operator Token(byte[] value)
        {
            return Business.Extensions.Help.ProtoBufDeserialize<Token>(value);
        }

        public override string ToString()
        {
            return Business.Extensions.Help.JsonSerialize(this);
        }

        public byte[] ToBytes()
        {
            return Business.Extensions.Help.ProtoBufSerialize(this);
        }

        [ProtoBuf.ProtoMember(1, Name = "K")]
        [Newtonsoft.Json.JsonProperty(PropertyName = "K")]
        public string Key { get; set; }

        [ProtoBuf.ProtoMember(2, Name = "R")]
        [Newtonsoft.Json.JsonProperty(PropertyName = "R")]
        public string Remote { get; set; }

        /// <summary>
        /// Socket identity
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string CommandID { get; set; }
    }
}
