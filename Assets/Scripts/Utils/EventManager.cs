using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EventTrigger
{

    public enum EventType
    {
        InvenCharacterSelect,   // 인벤토리에서 캐릭터 선택 이벤트 발생
        BoardCreateComplete,    // 초기 보드 생성
        CameraMoveComplete,     // 보드 생성 후 카메라 이동 완료
        RoundClear,             // 카메라 이동 완료 후 클리어 확인
        RoundEnd,               // 라운드 종료 (성공/실패 모두 포함)
        ScriptPrintEnd,         // 스크립트 출력 종료
    }

    public interface IEventListener
    {
        void OnEvent(EventType eventType, Component sender, object param = null);
    }
    public class EventManager : Singleton<EventManager>
    {
        private Dictionary<EventType, List<IEventListener>> _eventListners = new Dictionary<EventType, List<IEventListener>>();

        public void AddEvent(EventType eventType, IEventListener listner)
        {
            if (!_eventListners.ContainsKey(eventType))
            {
                _eventListners[eventType] = new List<IEventListener>();
            }
            _eventListners[eventType].Add(listner);
        }

        public void InvokeEvent(EventType eventType, Component sender, object param = null)
        {
            if (_eventListners.TryGetValue(eventType, out var listners))
            {
                foreach (var listner in listners)
                {
                    listner?.OnEvent(eventType, sender, param);
                }
            }
        }

        public void RemoveEvent(EventType eventType, IEventListener listner)
        {
            if (_eventListners.TryGetValue(eventType, out var listners))
            {
                listners.Remove(listner);

                if (listners.Count == 0)
                {
                    _eventListners.Remove(eventType);
                }
            }
        }

        public void EnsureIntegrity()
        {
            foreach (var eventType in _eventListners.Keys.ToList())
            {
                var listners = _eventListners[eventType].Where(listner => listner != null).ToList();

                if (listners.Count > 0)
                {
                    _eventListners[eventType] = listners;
                }
                else
                {
                    _eventListners.Remove(eventType);
                }
            }
        }

        public void ClearAllEvents()
        {
            _eventListners.Clear();
        }
    }
}