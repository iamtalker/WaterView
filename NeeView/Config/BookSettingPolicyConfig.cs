using CommunityToolkit.Mvvm.ComponentModel;
using Generator.Equals;
using NeeView.Windows.Property;
using System;

namespace NeeView
{
    [Equatable(Explicit = true, IgnoreInheritedMembers = true)]
    public partial class BookSettingPolicyConfig : ObservableObject, ICloneable
    {
        [DefaultEquality] private BookSettingPageSelectMode _page = BookSettingPageSelectMode.RestoreOrDefault;
        [DefaultEquality] private BookSettingSelectMode _pageMode = BookSettingSelectMode.Continue; // 물뷰: 한 쪽/두 쪽 보기는 책마다 기억하지 않고 지금 설정을 계속 쓴다
        [DefaultEquality] private BookSettingSelectMode _bookReadOrder = BookSettingSelectMode.Continue; // 물뷰: 읽는 방향은 책마다 기억하지 않고 지금 설정을 계속 쓴다
        [DefaultEquality] private BookSettingSelectMode _isSupportedDividePage = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _isSupportedSingleFirstPage = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _isSupportedSingleLastPage = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _isSupportedWidePage = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _isRecursiveFolder = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _sortMode = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _autoRotate = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _baseScale = BookSettingSelectMode.Continue;
        [DefaultEquality] private BookSettingSelectMode _effectProfileId = BookSettingSelectMode.Continue;

        [PropertyMember]
        public BookSettingPageSelectMode Page
        {
            get { return _page; }
            set { SetProperty(ref _page, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode PageMode
        {
            get { return _pageMode; }
            set { SetProperty(ref _pageMode, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode BookReadOrder
        {
            get { return _bookReadOrder; }
            set { SetProperty(ref _bookReadOrder, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode IsSupportedDividePage
        {
            get { return _isSupportedDividePage; }
            set { SetProperty(ref _isSupportedDividePage, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode IsSupportedSingleFirstPage
        {
            get { return _isSupportedSingleFirstPage; }
            set { SetProperty(ref _isSupportedSingleFirstPage, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode IsSupportedSingleLastPage
        {
            get { return _isSupportedSingleLastPage; }
            set { SetProperty(ref _isSupportedSingleLastPage, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode IsSupportedWidePage
        {
            get { return _isSupportedWidePage; }
            set { SetProperty(ref _isSupportedWidePage, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode IsRecursiveFolder
        {
            get { return _isRecursiveFolder; }
            set { SetProperty(ref _isRecursiveFolder, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode SortMode
        {
            get { return _sortMode; }
            set { SetProperty(ref _sortMode, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode AutoRotate
        {
            get { return _autoRotate; }
            set { SetProperty(ref _autoRotate, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode BaseScale
        {
            get { return _baseScale; }
            set { SetProperty(ref _baseScale, value); }
        }

        [PropertyMember]
        public BookSettingSelectMode EffectProfileId
        {
            get { return _effectProfileId; }
            set { SetProperty(ref _effectProfileId, value); }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

}
