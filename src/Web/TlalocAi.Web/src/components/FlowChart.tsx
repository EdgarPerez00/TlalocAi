import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { formatShortTime } from '../utils/dateFormat'
import { formatFlow } from '../utils/numberFormat'

interface FlowChartPoint {
  bucketUtc: string
  averageFlowLpm: number
}

interface FlowChartProps {
  data: FlowChartPoint[]
}

function FlowChart({ data }: FlowChartProps) {
  return (
    <div className="chart-shell">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data}>
          <defs>
            <linearGradient id="flowFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor="#0b6e88" stopOpacity={0.5} />
              <stop offset="95%" stopColor="#0b6e88" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid strokeDasharray="3 3" stroke="rgba(23, 50, 77, 0.1)" />
          <XAxis dataKey="bucketUtc" tickFormatter={formatShortTime} minTickGap={24} />
          <YAxis />
          <Tooltip
            formatter={(value) => formatFlow(Number(value ?? 0))}
            labelFormatter={(label) => formatShortTime(String(label))}
          />
          <Area
            type="monotone"
            dataKey="averageFlowLpm"
            stroke="#0b6e88"
            strokeWidth={2}
            fillOpacity={1}
            fill="url(#flowFill)"
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}

export default FlowChart
