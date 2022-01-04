using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayerDataDump
{
    public class GlobalSettings
    {
        public static event Action StyleEvent;
        public static event Action PresetEvent;
        public enum Style
        {
            Classic,
            Modern
        }
        public enum Profile
        {
            PlayerCustom1,
            PlayerCustom2,
            PlayerCustom3,
            Default,
            Race
        }
        public Style _TrackerStyle = Style.Classic;
        public Profile _TrackerProfile = Profile.PlayerCustom1;
        public Style TrackerStyle
        {
            get
            {
                return _TrackerStyle;
            }
            set
            {
                if(value != _TrackerStyle)
                {
                    _TrackerStyle = value;
                    StyleEvent();
                }
            }
        }
        public Profile TrackerProfile
        {
            get
            {
                return _TrackerProfile;
            }
            set
            {
                if (value != _TrackerProfile)
                {
                    _TrackerProfile = value;
                    PresetEvent();
                }
            }
        }
        

    }
}
