interface BrandMarkProps {
  className?: string
}

function BrandMark({ className = '' }: BrandMarkProps) {
  const classNames = ['brand-mark', className].filter(Boolean).join(' ')

  return (
    <span className={classNames} aria-hidden="true">
      <img className="brand-mark-image" src="/assets/tlalocai-icon.png" alt="" />
    </span>
  )
}

export default BrandMark
